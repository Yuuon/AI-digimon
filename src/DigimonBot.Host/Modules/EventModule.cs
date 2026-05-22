using DigimonBot.Core.Events;
using DigimonBot.Core.Modules;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host.Modules;

/// <summary>
/// Event module - subscribes to internal events (evolution ready, tavern auto-speak)
/// and sends the appropriate messages to users/groups.
/// Can be hot-reloaded to re-subscribe to events.
/// </summary>
public class EventModule : IModule
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EventModule> _logger;
    private IModuleContext? _context;

    public string Name => "Event";
    public string Description => "事件模块 - 处理进化通知和酒馆自主发言事件";

    public EventModule(IEventPublisher eventPublisher, ILogger<EventModule> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task InitializeAsync(IModuleContext context)
    {
        _context = context;

        // Subscribe to tavern auto-speak event
        _eventPublisher.OnTavernAutoSpeak += HandleTavernAutoSpeak;

        // Subscribe to evolution ready event
        _eventPublisher.OnEvolutionReady += HandleEvolutionReady;

        _logger.LogInformation("EventModule initialized - subscribed to events");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _eventPublisher.OnTavernAutoSpeak -= HandleTavernAutoSpeak;
        _eventPublisher.OnEvolutionReady -= HandleEvolutionReady;
        _logger.LogInformation("EventModule shutdown - unsubscribed from events");
        return Task.CompletedTask;
    }

    public Task<ModuleResult> HandleMessageAsync(ModuleMessage message)
    {
        // This module doesn't handle messages directly
        return Task.FromResult(ModuleResult.NotHandled());
    }

    public Task HandleEventAsync(ModuleEvent evt)
    {
        return Task.CompletedTask;
    }

    private async void HandleTavernAutoSpeak(object? sender, TavernAutoSpeakEventArgs args)
    {
        try
        {
            if (_context == null) return;
            _logger.LogInformation("EventModule: tavern auto-speak for Group={GroupId}", args.GroupId);
            await _context.SendGroupMessageAsync(args.GroupId, args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventModule: failed to send tavern auto-speak");
        }
    }

    private async void HandleEvolutionReady(object? sender, EvolutionReadyEventArgs args)
    {
        try
        {
            if (_context == null) return;
            _logger.LogInformation("EventModule: evolution ready for User={UserId}", args.UserId);

            var message = BuildEvolutionReadyMessage(args);

            if (args.GroupId > 0)
            {
                await _context.SendGroupMessageAsync(args.GroupId, message);
            }
            else
            {
                var userId = args.UserId.Split('@')[0];
                if (long.TryParse(userId, out var qq))
                {
                    await _context.SendPrivateMessageAsync(qq, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventModule: failed to send evolution notification");
        }
    }

    private static string BuildEvolutionReadyMessage(EvolutionReadyEventArgs args)
    {
        var options = new List<string>();
        for (int i = 0; i < args.AvailableEvolutions.Count; i++)
        {
            var evo = args.AvailableEvolutions[i];
            options.Add($"**{i + 1}. {evo.TargetName}** - {evo.Description}");
        }

        return $"""
            🌟 **{args.CurrentDigimonName}** 可以进化了！

            检测到 **{args.AvailableEvolutions.Count}** 个可进化分支：

            {string.Join("\n", options)}

            使用 `/evoselect <序号>` 选择想要进化的分支
            例如：`/evoselect 1`
            """;
    }
}
