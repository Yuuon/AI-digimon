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
    private readonly ITavernService _tavernService;
    private readonly IGroupChatMonitorService _groupChatMonitor;

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
        IGroupModeConfig groupModeConfig,
        ITavernService tavernService,
        IGroupChatMonitorService groupChatMonitor)
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
        _tavernService = tavernService;
        _groupChatMonitor = groupChatMonitor;
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

        // 2. 群聊监测（酒馆模式下的关键词检测）
        if (context.IsGroupMessage && context.GroupId.HasValue)
        {
            _groupChatMonitor.AddMessage(context.GroupId.Value, context.UserId, context.UserName, context.Content);
            
            // 如果酒馆模式激活、机器人被@，且消息以"/酒馆对话"开头，才处理酒馆对话
            if (_tavernService.IsEnabled && context.IsMentioned && 
                context.Content.TrimStart().StartsWith("/酒馆对话", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleTavernConversationAsync(context);
            }
            
            // 检查是否触发自主发言（酒馆模式下，关键词高频出现）
            if (_tavernService.IsEnabled && _tavernService.HasCharacterLoaded())
            {
                var gid = context.GroupId.Value;
                _logger.LogInformation("[自主发言] 准备检查群 {GroupId} 的触发条件", gid);
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await CheckAndTriggerTavernAutoSpeakAsync(gid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[自主发言] Task.Run 内异常: {Message}", ex.Message);
                    }
                });
            }
        }

        // 3. 处理普通AI对话
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

        // 处理查看他人数据的逻辑
        string? targetUserId = null;
        string? targetOriginalUserId = null;
        
        if (context.IsGroupMessage && args.Length > 0)
        {
            // 优先从@提及获取目标用户
            if (context.MentionedUserIds.Count > 0)
            {
                targetOriginalUserId = context.MentionedUserIds[0];
                targetUserId = GenerateUserId(targetOriginalUserId, context.GroupId);
            }
            // 否则尝试解析手动输入的QQ号
            else if (long.TryParse(args[0], out var targetQQ))
            {
                targetOriginalUserId = targetQQ.ToString();
                targetUserId = GenerateUserId(targetOriginalUserId, context.GroupId);
            }
        }

        var cmdContext = new CommandContext
        {
            UserId = userId,
            OriginalUserId = context.UserId,
            UserName = context.UserName,
            Message = content,
            Args = args,
            GroupId = context.GroupId ?? 0,
            IsGroupMessage = context.IsGroupMessage,
            ShouldAddPrefix = ShouldAddUserPrefix(context),
            MentionedUserIds = context.MentionedUserIds,
            TargetUserId = targetUserId,
            TargetOriginalUserId = targetOriginalUserId
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
                    _logger.LogInformation("开始情感分析: User={UserId}, Message={Message}", userId, context.Content);
                    
                    var emotionAnalysis = await _aiClient.AnalyzeEmotionAsync(context.Content, aiResponse.Content);
                    
                    _logger.LogInformation("情感分析完成: Courage={C}, Friendship={F}, Love={L}, Knowledge={K}",
                        emotionAnalysis.CourageDelta, emotionAnalysis.FriendshipDelta, 
                        emotionAnalysis.LoveDelta, emotionAnalysis.KnowledgeDelta);
                    
                    // 记录修改前的值
                    var oldCourage = userDigimon.Emotions.Courage;
                    var oldFriendship = userDigimon.Emotions.Friendship;
                    var oldLove = userDigimon.Emotions.Love;
                    var oldKnowledge = userDigimon.Emotions.Knowledge;
                    
                    await _emotionTracker.ApplyEmotionAnalysisAsync(userDigimon, emotionAnalysis, "对话分析");
                    
                    _logger.LogInformation("情感值已修改: 勇气{OldC}->{NewC}, 友情{OldF}->{NewF}, 爱心{OldL}->{NewL}, 知识{OldK}->{NewK}",
                        oldCourage, userDigimon.Emotions.Courage,
                        oldFriendship, userDigimon.Emotions.Friendship,
                        oldLove, userDigimon.Emotions.Love,
                        oldKnowledge, userDigimon.Emotions.Knowledge);
                    
                    // 保存情感变化到数据库
                    await _digimonManager.SaveAsync(userDigimon);
                    
                    _logger.LogInformation("情感变化已保存到数据库: User={UserId}", userId);
                    
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
                    _logger.LogError(ex, "情感分析异常: {Message}", ex.Message);
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

    private async Task<MessageResult> HandleTavernConversationAsync(MessageContext context)
    {
        if (!context.GroupId.HasValue)
        {
            return new MessageResult { Handled = false };
        }

        var groupId = context.GroupId.Value;
        
        try
        {
            _logger.LogInformation("处理酒馆对话: Group={GroupId}, User={UserId}", groupId, context.UserId);
            
            // 去除 "/酒馆对话" 前缀，获取实际对话内容
            var dialogueContent = context.Content.TrimStart();
            if (dialogueContent.StartsWith("/酒馆对话", StringComparison.OrdinalIgnoreCase))
            {
                dialogueContent = dialogueContent.Substring("/酒馆对话".Length).TrimStart();
            }
            
            // 如果内容为空，给出提示
            if (string.IsNullOrWhiteSpace(dialogueContent))
            {
                return new MessageResult 
                { 
                    Handled = true, 
                    Response = "（你想对角色说什么呢？在/酒馆对话后面加上你想说的话吧~）"
                };
            }
            
            // 处理酒馆对话
            var response = await _tavernService.GenerateResponseAsync(dialogueContent, context.UserName);
            
            _logger.LogInformation("酒馆对话响应: Group={GroupId}, Response={Response}", groupId, response);
            
            return new MessageResult
            {
                Handled = true,
                Response = response
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "酒馆对话处理异常");
            return new MessageResult 
            { 
                Handled = true, 
                Response = "（角色似乎沉浸在自己的世界里，没有听到你的话...）"
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

    /// <summary>
    /// 检查并触发酒馆自主发言（关键词高频时）
    /// </summary>
    private async Task CheckAndTriggerTavernAutoSpeakAsync(long groupId)
    {
        try
        {
            _logger.LogInformation("[自主发言] 开始检查群 {GroupId}", groupId);
            
            // 先检查状态
            var status = _groupChatMonitor.GetGroupStatus(groupId);
            _logger.LogInformation("[自主发言] 群 {GroupId} 状态: 消息={Count}, 关键词={HasKeyword}, 冷却={Cooldown}", 
                groupId, status.MessageCount, status.HasHighFreqKeyword, status.IsInCooldown);
            
            if (!status.CanTrigger)
            {
                _logger.LogInformation("[自主发言] 群 {GroupId} 不满足触发条件", groupId);
                return;
            }

            _logger.LogInformation("[自主发言] 群 {GroupId} 满足触发条件，关键词: {Keywords}", 
                groupId, string.Join(",", status.TopKeywords.Select(kv => $"{kv.Key}({kv.Value})")));

            // 生成群聊总结
            var summary = await _groupChatMonitor.GenerateSummaryAsync(groupId);
            _logger.LogInformation("[自主发言] 群 {GroupId} 总结生成完成: {Summary}", groupId, summary[..Math.Min(50, summary.Length)]);
            
            // 生成角色回复
            var keywords = string.Join(",", status.TopKeywords.Take(3).Select(kv => kv.Key));
            var response = await _tavernService.GenerateSummaryResponseAsync(summary, keywords);
            _logger.LogInformation("[自主发言] 群 {GroupId} 回复生成完成: {Response}", groupId, response[..Math.Min(50, response.Length)]);
            
            // 构建带角色名的回复
            var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
            var message = $"🎭 **{characterName}**（听到你们讨论得热烈，忍不住插话）\n\n{response}";
            
            _logger.LogInformation("[自主发言] 群 {GroupId} 准备发布事件", groupId);
            
            // 发布自主发言事件（由 BotService 监听并发送）
            _eventPublisher.PublishTavernAutoSpeak(new TavernAutoSpeakEventArgs
            {
                GroupId = groupId,
                Message = message
            });
            
            _logger.LogInformation("[自主发言] 群 {GroupId} 事件已发布", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[自主发言] 处理异常: {Message}", ex.Message);
        }
    }
}
