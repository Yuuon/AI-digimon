using DigimonBot.AI.Services;
using DigimonBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// "这是什么"识图指令
/// </summary>
public class WhatIsThisCommand : ICommand
{
    private readonly IVisionService _visionService;
    private readonly IMessageHistoryService _messageHistory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WhatIsThisCommand> _logger;

    public WhatIsThisCommand(
        IVisionService visionService,
        IMessageHistoryService messageHistory,
        IServiceProvider serviceProvider,
        ILogger<WhatIsThisCommand> logger)
    {
        _visionService = visionService;
        _messageHistory = messageHistory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string Name => "whatisthis";
    public string[] Aliases => new[] { "这是什么", "识图", "img" };
    public string Description => "识别图片内容（检查最近3条消息中的图片）";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 检查识图功能是否可用
        if (!_visionService.IsAvailable)
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 识图功能未配置。请在配置文件中设置 VisionModel。"
            };
        }

        _logger.LogInformation("[WhatIsThis] 开始处理指令: User={User}, Group={Group}, OriginalUserId={OriginalUserId}",
            context.UserId, context.GroupId, context.OriginalUserId);

        // 获取最近的消息历史
        var recentMessages = _messageHistory.GetRecentMessages(
            context.OriginalUserId, 
            context.GroupId, 
            count: 5); // 多取一些，因为可能包含当前指令消息

        _logger.LogInformation("[WhatIsThis] 获取到{Count}条历史消息", recentMessages.Count);

        if (recentMessages.Count == 0)
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 没有找到历史消息。请发送一张图片后再使用此指令。"
            };
        }

        // 查找最近3条消息中的图片（排除当前指令消息）
        string? imageUrl = null;
        string? imageFile = null;
        int checkedCount = 0;
        
        foreach (var message in recentMessages)
        {
            _logger.LogDebug("[WhatIsThis] 检查消息: Type={Type}, IsFromBot={IsFromBot}, Content={Content}",
                message.Type, message.IsFromBot, message.Content?.Substring(0, Math.Min(50, message.Content?.Length ?? 0)));
            
            // 跳过Bot自己的回复
            if (message.IsFromBot)
                continue;
            
            // 跳过当前指令消息（文本类型的指令）
            if (message.Type == "text" && IsCommandMessage(message.Content))
                continue;
            
            checkedCount++;
            
            // 找到图片
            if (message.Type == "image")
            {
                imageUrl = message.ImageUrl;
                imageFile = message.ImageFile;
                _logger.LogInformation("[WhatIsThis] 找到图片: Url={Url}, File={File}, 在倒数第 {Count} 条消息", 
                    imageUrl, imageFile, checkedCount);
                break;
            }
            
            // 最多检查3条非Bot消息
            if (checkedCount >= 3)
                break;
        }

        if (string.IsNullOrEmpty(imageUrl) && string.IsNullOrEmpty(imageFile))
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 没有找到图片。\n请在最近3条消息内发送一张图片，然后使用 `/这是什么` 指令。"
            };
        }

        // 调用识图服务
        try
        {
            string? finalImageUrl = imageUrl;
            
            // 如果没有URL但有File，尝试获取真实URL
            if (string.IsNullOrEmpty(finalImageUrl) && !string.IsNullOrEmpty(imageFile))
            {
                _logger.LogInformation("尝试使用ImageFile获取真实URL: {File}", imageFile);
                // 延迟解析 IImageUrlResolver 以避免启动时的循环依赖
                var imageUrlResolver = _serviceProvider.GetRequiredService<IImageUrlResolver>();
                finalImageUrl = await imageUrlResolver.ResolveImageUrlAsync(imageFile);
                
                if (string.IsNullOrEmpty(finalImageUrl))
                {
                    return new CommandResult
                    {
                        Success = false,
                        Message = "❌ 无法获取图片访问链接。请稍后再试。"
                    };
                }
            }
            
            _logger.LogInformation("开始分析图片: {Url}", finalImageUrl);
            
            var result = await _visionService.AnalyzeImageAsync(finalImageUrl!, "这是什么？请详细描述图片内容。");
            
            _logger.LogInformation("图片分析完成");

            return new CommandResult
            {
                Success = true,
                Message = $"🖼️ **图片分析结果**\n\n{result}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片分析失败");
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 图片分析失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 判断是否是指令消息
    /// </summary>
    private static bool IsCommandMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
            
        var trimmed = content.Trim();
        return trimmed.StartsWith('/') || 
               trimmed.StartsWith('！') || 
               trimmed.StartsWith('!');
    }
}
