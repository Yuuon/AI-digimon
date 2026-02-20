using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 每日签到指令
/// </summary>
public class CheckInCommand : ICommand
{
    private readonly ICheckInRepository _checkInRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _digimonRepository;
    private readonly IAIClient _aiClient;
    private readonly IPersonalityEngine _personalityEngine;
    private readonly ILogger<CheckInCommand> _logger;

    public CheckInCommand(
        ICheckInRepository checkInRepository,
        IInventoryRepository inventoryRepository,
        IItemRepository itemRepository,
        IDigimonManager digimonManager,
        IDigimonRepository digimonRepository,
        IAIClient aiClient,
        IPersonalityEngine personalityEngine,
        ILogger<CheckInCommand> logger)
    {
        _checkInRepository = checkInRepository;
        _inventoryRepository = inventoryRepository;
        _itemRepository = itemRepository;
        _digimonManager = digimonManager;
        _digimonRepository = digimonRepository;
        _aiClient = aiClient;
        _personalityEngine = personalityEngine;
        _logger = logger;
    }

    public string Name => "checkin";
    public string[] Aliases => new[] { "签到", "sign", "打卡" };
    public string Description => "每日签到，获得奖励并与数码宝贝互动";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 检查今天是否已经签到
        var hasCheckedIn = await _checkInRepository.HasCheckedInTodayAsync(context.UserId);
        if (hasCheckedIn)
        {
            var record = await _checkInRepository.GetAsync(context.UserId);
            return new CommandResult
            {
                Success = false,
                Message = $"📅 **今日已签到！**\n\n" +
                         $"总签到天数：**{record?.TotalCheckIns ?? 0}** 天\n" +
                         $"连续签到：**{record?.ConsecutiveCheckIns ?? 0}** 天\n\n" +
                         $"明天再来继续签到吧~"
            };
        }

        // 执行签到
        var checkInRecord = await _checkInRepository.CheckInAsync(context.UserId);
        
        // 根据连续签到天数计算奖励
        var rewardItem = await GetRewardAsync(checkInRecord.ConsecutiveCheckIns);
        
        // 添加物品到背包
        if (rewardItem != null)
        {
            await _inventoryRepository.AddItemAsync(context.UserId, rewardItem.Id);
        }

        // 触发数码宝贝对话
        var digimonResponse = await GenerateDigimonResponseAsync(context.UserId);

        // 构建回复消息
        var prefix = context.ShouldAddPrefix && !string.IsNullOrWhiteSpace(context.UserName)
            ? $"[{context.UserName}]的"
            : "";

        var message = $"📅 **{prefix}每日签到成功！**\n\n" +
                     $"✅ 总签到天数：**{checkInRecord.TotalCheckIns}** 天\n" +
                     $"🔥 连续签到：**{checkInRecord.ConsecutiveCheckIns}** 天\n\n";

        if (rewardItem != null)
        {
            message += $"🎁 签到奖励：**{rewardItem.Name}**\n" +
                      $"   {rewardItem.Description}\n\n";
        }

        if (!string.IsNullOrEmpty(digimonResponse))
        {
            message += $"💬 {prefix}数码宝贝：\n{digimonResponse}";
        }

        _logger.LogInformation("用户 {UserId} 签到成功，连续签到 {Consecutive} 天，获得奖励 {Reward}",
            context.UserId, checkInRecord.ConsecutiveCheckIns, rewardItem?.Name ?? "无");

        return new CommandResult
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// 根据连续签到天数获取奖励
    /// </summary>
    private async Task<ItemDefinition?> GetRewardAsync(int consecutiveDays)
    {
        // 获取所有食物类物品
        var allItems = _itemRepository.GetAll().Values
            .Where(i => i.Type == "food" && i.Price > 0)
            .OrderBy(i => i.Price)
            .ToList();

        if (allItems.Count == 0)
            return null;

        // 计算获得高品级食物的概率
        // 连续1天: 0%, 连续15天: 50%, 连续30天: 100%
        var highTierProbability = Math.Min(1.0, (double)consecutiveDays / 30.0);
        
        var random = new Random();
        var roll = random.NextDouble();

        ItemDefinition selectedItem;
        
        if (roll < highTierProbability && allItems.Count > 1)
        {
            // 获得高品级食物（价格最高的）
            var highTierItems = allItems.Where(i => i.Price >= 150).ToList();
            selectedItem = highTierItems.Count > 0 
                ? highTierItems[random.Next(highTierItems.Count)]
                : allItems.Last();
            
            _logger.LogDebug("用户获得高品级奖励: {Item}, 概率: {Prob:P}", selectedItem.Name, highTierProbability);
        }
        else
        {
            // 获得普通食物（价格较低的）
            var normalItems = allItems.Where(i => i.Price < 150).ToList();
            selectedItem = normalItems.Count > 0
                ? normalItems[random.Next(normalItems.Count)]
                : allItems.First();
            
            _logger.LogDebug("用户获得普通奖励: {Item}, 概率: {Prob:P}", selectedItem.Name, 1 - highTierProbability);
        }

        // 连续30天特殊奖励：必定获得最高品级（盛宴拼盘）
        if (consecutiveDays >= 30)
        {
            var feastPlatter = allItems.FirstOrDefault(i => i.Id == "feast_platter");
            if (feastPlatter != null)
            {
                selectedItem = feastPlatter;
                _logger.LogInformation("用户连续签到30天，获得特殊奖励: {Item}", selectedItem.Name);
            }
        }

        return selectedItem;
    }

    /// <summary>
    /// 生成数码宝贝的签到回应
    /// </summary>
    private async Task<string> GenerateDigimonResponseAsync(string userId)
    {
        try
        {
            var userDigimon = await _digimonManager.GetOrCreateAsync(userId);
            var definition = _digimonRepository.GetById(userDigimon.CurrentDigimonId);
            
            if (definition == null)
                return "";

            // 构建签到相关的提示词
            var checkInPrompts = new[]
            {
                "主人今天来陪我玩了！好开心呀~",
                "又见到你了，今天也要元气满满哦！",
                "主人准时来看我了，我好幸福~",
                "今天的签到完成了，接下来一起冒险吧！",
                "嘿嘿，主人记得我，我好开心！",
                "新的一天，新的陪伴，最喜欢主人了！",
                "主人来了！今天有什么好玩的吗？",
                "签到成功！主人是最棒的！",
                "每天都等着主人来，终于等到了~",
                "和主人在一起的每一天都很特别！"
            };

            var random = new Random();
            var basePrompt = checkInPrompts[random.Next(checkInPrompts.Length)];

            // 构建系统提示词，让AI基于数码宝贝性格生成回应
            var systemPrompt = _personalityEngine.BuildSystemPrompt(definition, userDigimon);
            
            var messages = new List<ChatMessage>
            {
                new() { IsFromUser = true, Content = $"（每日签到时间）主人来签到啦！你想对主人说什么？参考：{basePrompt}" }
            };

            var response = await _aiClient.ChatAsync(messages, systemPrompt);
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成签到回应失败");
            // 失败时返回简单的预设回应
            var fallbacks = new[]
            {
                "主人来啦！今天也要开心哦~",
                "签到成功！我最喜欢主人了！",
                "又见到主人了，好开心！"
            };
            return fallbacks[new Random().Next(fallbacks.Length)];
        }
    }
}
