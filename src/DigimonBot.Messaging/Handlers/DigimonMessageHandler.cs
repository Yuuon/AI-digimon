using DigimonBot.AI.Services;
using DigimonBot.Core.Events;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using DigimonBot.Messaging.Commands;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Handlers;

/// <summary>
/// 数码宝贝消息处理器
/// </summary>
public class DigimonMessageHandler : IMessageHandler
{
    private readonly CommandRegistry _commandRegistry;
    private readonly IDigimonManager _digimonManager;
    private readonly IDigimonRepository _repository;
    private readonly IAIClient _aiClient;
    private readonly IPersonalityEngine _personalityEngine;
    private readonly IEvolutionEngine _evolutionEngine;
    private readonly IEmotionTracker _emotionTracker;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DigimonMessageHandler> _logger;
    private readonly IGroupModeConfig _groupModeConfig;

    public DigimonMessageHandler(
        CommandRegistry commandRegistry,
        IDigimonManager digimonManager,
        IDigimonRepository repository,
        IAIClient aiClient,
        IPersonalityEngine personalityEngine,
        IEvolutionEngine evolutionEngine,
        IEmotionTracker emotionTracker,
        IEventPublisher eventPublisher,
        ILogger<DigimonMessageHandler> logger,
        IGroupModeConfig groupModeConfig)
    {
        _commandRegistry = commandRegistry;
        _digimonManager = digimonManager;
        _repository = repository;
        _aiClient = aiClient;
        _personalityEngine = personalityEngine;
        _evolutionEngine = evolutionEngine;
        _emotionTracker = emotionTracker;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _groupModeConfig = groupModeConfig;
    }
    
    /// <summary>
    /// 生成用户ID（根据群聊模式决定）
    /// </summary>
    private string GenerateUserId(string originalUserId, long? groupId)
    {
        // 私聊或共同培养模式：使用原始用户ID
        if (groupId == null || groupId == 0 || 
            _groupModeConfig.GroupDigimonMode.Equals("Shared", StringComparison.OrdinalIgnoreCase))
        {
            return originalUserId;
        }
        
        // 各自培养模式：userId + groupId
        return $"{originalUserId}@g{groupId}";
    }

    public async Task<MessageResult> HandleMessageAsync(MessageContext context)
    {
        var content = context.Content.Trim();
        
        // 1. 检查是否为命令
        if (IsCommand(content))
        {
            return await HandleCommandAsync(context, content);
        }

        // 2. 处理AI对话
        return await HandleAiConversationAsync(context);
    }

    private bool IsCommand(string content)
    {
        return content.StartsWith('/') || content.StartsWith('！') || content.StartsWith('!');
    }

    /// <summary>
    /// 判断是否需要在回复中添加用户前缀（各自培养模式下的群聊）
    /// </summary>
    private bool ShouldAddUserPrefix(MessageContext context)
    {
        return context.IsGroupMessage && 
               _groupModeConfig.GroupDigimonMode.Equals("Separate", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建前缀（格式：[群昵称]的XX兽：）
    /// </summary>
    private string BuildPrefix(MessageContext context, string digimonName)
    {
        if (!ShouldAddUserPrefix(context))
            return string.Empty;
        
        var displayName = string.IsNullOrWhiteSpace(context.UserName) ? context.UserId : context.UserName;
        return $"[{displayName}]的{digimonName}：";
    }

    private async Task<MessageResult> HandleCommandAsync(MessageContext context, string content)
    {
        // 去除前缀
        content = content.TrimStart('/', '！', '!');
        
        // 解析命令和参数
        var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var commandName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (!_commandRegistry.TryGetCommand(commandName, out var command) || command == null)
        {
            return new MessageResult 
            { 
                Handled = true, 
                Response = "未知命令，发送 /help 查看可用命令。"
            };
        }

        // 生成带群聊隔离的UserId
        var userId = GenerateUserId(context.UserId, context.GroupId);

        var cmdContext = new CommandContext
        {
            UserId = userId,
            OriginalUserId = context.UserId,
            UserName = context.UserName,
            Message = content,
            Args = args,
            GroupId = context.GroupId,
            IsGroupMessage = context.IsGroupMessage,
            ShouldAddPrefix = ShouldAddUserPrefix(context)
        };

        var result = await command.ExecuteAsync(cmdContext);
        
        return new MessageResult
        {
            Handled = true,
            Response = result.Message,
            IsCommand = true
        };
    }

    private async Task<MessageResult> HandleAiConversationAsync(MessageContext context)
    {
        // 生成带群聊隔离的UserId
        var userId = GenerateUserId(context.UserId, context.GroupId);
        
        // 获取或创建用户数码宝贝
        var userDigimon = await _digimonManager.GetOrCreateAsync(userId);
        var definition = _repository.GetById(userDigimon.CurrentDigimonId);
        
        if (definition == null)
        {
            _logger.LogError("Digimon definition not found for {DigimonId}", userDigimon.CurrentDigimonId);
            return new MessageResult { Handled = false };
        }

        try
        {
            // 构建系统提示词
            var systemPrompt = _personalityEngine.BuildSystemPrompt(definition, userDigimon);
            
            // 调用AI获取回复
            var aiResponse = await _aiClient.ChatAsync(userDigimon.ChatHistory, systemPrompt);
            
            // 分析情感（异步，不阻塞回复）
            _ = Task.Run(async () =>
            {
                try
                {
                    var emotionAnalysis = await _aiClient.AnalyzeEmotionAsync(context.Content, aiResponse.Content);
                    await _emotionTracker.ApplyEmotionAnalysisAsync(userDigimon, emotionAnalysis, "对话分析");
                    
                    // 发布情感变化事件
                    _eventPublisher.PublishEmotionChanged(new EmotionChangedEventArgs
                    {
                        UserId = userId,
                        DigimonId = userDigimon.CurrentDigimonId,
                        Changes = new Dictionary<EmotionType, int>
                        {
                            [EmotionType.Courage] = emotionAnalysis.CourageDelta,
                            [EmotionType.Friendship] = emotionAnalysis.FriendshipDelta,
                            [EmotionType.Love] = emotionAnalysis.LoveDelta,
                            [EmotionType.Knowledge] = emotionAnalysis.KnowledgeDelta
                        },
                        Reason = emotionAnalysis.Reasoning
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in emotion analysis");
                }
            });

            // 记录对话
            await _digimonManager.RecordConversationAsync(
                userId, 
                context.Content, 
                aiResponse.Content, 
                aiResponse.TotalTokens,
                null // 情感分析稍后补充
            );

            // 检查进化
            var evolutionResult = await CheckEvolutionAsync(userId, userDigimon);

            // 构建回复（添加前缀）
            var response = aiResponse.Content;
            if (ShouldAddUserPrefix(context))
            {
                var prefix = BuildPrefix(context, definition.Name);
                response = prefix + response;
            }

            return new MessageResult
            {
                Handled = true,
                Response = response,
                EvolutionOccurred = evolutionResult != null,
                EvolutionMessage = evolutionResult?.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI conversation");
            return new MessageResult 
            { 
                Handled = true, 
                Response = "（数码宝贝似乎有点困惑，请稍后再试...）"
            };
        }
    }

    private async Task<EvolutionResult?> CheckEvolutionAsync(string userId, UserDigimon userDigimon)
    {
        var digimonDb = _repository.GetAll();
        var evolutionResult = await _evolutionEngine.CheckAndEvolveAsync(userDigimon, digimonDb);
        
        if (evolutionResult != null && evolutionResult.Success)
        {
            // 执行进化
            await _digimonManager.UpdateDigimonAsync(userId, evolutionResult.NewDigimonId);
            
            // 如果是重生，重置Token计数
            if (evolutionResult.IsRebirth)
            {
                userDigimon.TotalTokensConsumed = 0;
                userDigimon.Emotions = new EmotionValues();
            }

            // 发布进化事件
            _eventPublisher.PublishEvolution(new EvolutionEventArgs
            {
                UserId = userId,
                OldDigimonId = evolutionResult.OldDigimonId,
                NewDigimonId = evolutionResult.NewDigimonId,
                NewDigimonName = evolutionResult.NewDigimonName,
                IsRebirth = evolutionResult.IsRebirth,
                EvolutionDescription = evolutionResult.Message
            });

            _logger.LogInformation("User {UserId} evolved from {Old} to {New}", 
                userId, evolutionResult.OldDigimonId, evolutionResult.NewDigimonId);

            return evolutionResult;
        }

        return null;
    }
}
