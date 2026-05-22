using DigimonBot.Core.Modules;
using DigimonBot.Messaging.Handlers;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host.Modules;

/// <summary>
/// Core Digimon module that handles all command/AI chat messages.
/// Wraps the existing IMessageHandler (DigimonMessageHandler) as a module.
/// </summary>
public class DigimonModule : IModule
{
    private readonly IMessageHandler _messageHandler;
    private readonly ILogger<DigimonModule> _logger;
    private IModuleContext? _context;

    public string Name => "Digimon";
    public string Description => "核心数码宝贝模块 - 处理所有命令和AI对话";

    public DigimonModule(IMessageHandler messageHandler, ILogger<DigimonModule> logger)
    {
        _messageHandler = messageHandler;
        _logger = logger;
    }

    public Task InitializeAsync(IModuleContext context)
    {
        _context = context;
        _logger.LogInformation("DigimonModule initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _logger.LogInformation("DigimonModule shutdown");
        return Task.CompletedTask;
    }

    public async Task<ModuleResult> HandleMessageAsync(ModuleMessage message)
    {
        // Convert ModuleMessage to MessageContext for the existing handler
        var context = new MessageContext
        {
            UserId = message.UserId,
            OriginalUserId = message.OriginalUserId,
            UserName = message.UserName,
            Content = message.Content,
            GroupId = message.GroupId,
            IsGroupMessage = message.IsGroupMessage,
            IsMentioned = message.IsMentioned,
            Timestamp = message.Timestamp,
            Source = message.IsGroupMessage ? MessageSource.Group : MessageSource.Private,
            MentionedUserIds = message.MentionedUserIds
        };

        var result = await _messageHandler.HandleMessageAsync(context);

        return new ModuleResult
        {
            Handled = result.Handled,
            Response = result.Response,
            AdditionalMessages = result.AdditionalMessages,
            EvolutionOccurred = result.EvolutionOccurred,
            EvolutionMessage = result.EvolutionMessage
        };
    }

    public Task HandleEventAsync(ModuleEvent evt)
    {
        // The Digimon module doesn't handle raw events
        return Task.CompletedTask;
    }
}
