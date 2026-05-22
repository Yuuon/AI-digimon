using DigimonBot.Core.Modules;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host.Modules;

/// <summary>
/// Tavern module - handles tavern auto-speak and special focus user responses.
/// Monitors group chat activity and triggers AI-generated responses based on configured rules.
/// Can be hot-reloaded to pick up new tavern configuration.
/// </summary>
public class TavernModule : IModule
{
    private readonly ITavernService _tavernService;
    private readonly ITavernConfigService _tavernConfigService;
    private readonly IGroupChatMonitorService _groupChatMonitor;
    private readonly ILogger<TavernModule> _logger;
    private IModuleContext? _context;

    // Special focus cooldown tracking: Key = "{groupId}:{userId}"
    private readonly Dictionary<string, DateTime> _specialFocusCooldown = new();

    public string Name => "Tavern";
    public string Description => "酒馆模块 - 自主发言和特别关注";

    public TavernModule(
        ITavernService tavernService,
        ITavernConfigService tavernConfigService,
        IGroupChatMonitorService groupChatMonitor,
        ILogger<TavernModule> logger)
    {
        _tavernService = tavernService;
        _tavernConfigService = tavernConfigService;
        _groupChatMonitor = groupChatMonitor;
        _logger = logger;
    }

    public Task InitializeAsync(IModuleContext context)
    {
        _context = context;
        _specialFocusCooldown.Clear();
        _logger.LogInformation("TavernModule initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _specialFocusCooldown.Clear();
        _logger.LogInformation("TavernModule shutdown");
        return Task.CompletedTask;
    }

    /// <summary>
    /// The TavernModule does not consume messages (returns NotHandled).
    /// Instead it observes group messages for auto-speak/special-focus triggers.
    /// </summary>
    public async Task<ModuleResult> HandleMessageAsync(ModuleMessage message)
    {
        // Only observe group messages
        if (!message.IsGroupMessage || !message.GroupId.HasValue)
            return ModuleResult.NotHandled();

        var groupId = message.GroupId.Value;

        // Record the message in the group chat monitor
        _groupChatMonitor.AddMessage(groupId, message.OriginalUserId, message.UserName, message.Content);

        // Check tavern auto-speak (fire and forget, non-blocking)
        _ = Task.Run(async () => await CheckTavernAutoSpeakAsync(groupId));

        // Check special focus (fire and forget, non-blocking)
        _ = Task.Run(async () => await CheckSpecialFocusAsync(
            groupId, message.OriginalUserId, message.UserName, message.Content, message.IsMentioned));

        // This module observes but doesn't consume the message
        return ModuleResult.NotHandled();
    }

    public Task HandleEventAsync(ModuleEvent evt)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check and trigger tavern auto-speak for a group.
    /// </summary>
    private async Task CheckTavernAutoSpeakAsync(long groupId)
    {
        try
        {
            if (!_tavernService.IsEnabled || !_tavernService.HasCharacterLoaded())
                return;

            var status = _groupChatMonitor.GetGroupStatus(groupId);
            if (!status.CanTrigger)
                return;

            if (!_tavernConfigService.Config.AutoSpeak.Enabled)
                return;

            _logger.LogInformation("[TavernModule] Group {GroupId} trigger condition met, generating reply", groupId);

            var summary = await _groupChatMonitor.GenerateSummaryAsync(groupId);
            var keywords = string.Join(",", status.TopKeywords.Take(3).Select(kv => kv.Key));
            var response = await _tavernService.GenerateSummaryResponseAsync(summary, keywords);

            var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
            var messagePrefix = _tavernConfigService.Config.AutoSpeak.MessagePrefix;
            var message = messagePrefix.Replace("{CharacterName}", characterName) + response;

            if (_context != null)
            {
                await _context.SendGroupMessageAsync(groupId, message);
            }

            _groupChatMonitor.RecordTriggerTime(groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TavernModule] Auto-speak check error");
        }
    }

    /// <summary>
    /// Check if a special focus user spoke and generate a response.
    /// </summary>
    private async Task CheckSpecialFocusAsync(long groupId, string userId, string userName, string content, bool isAtBot)
    {
        try
        {
            var config = _tavernConfigService.Config.SpecialFocus;

            if (!config.Enabled)
                return;

            if (!_tavernService.IsEnabled || !_tavernService.HasCharacterLoaded())
                return;

            if (config.RequireMention && !isAtBot)
                return;

            // Check if user is in special focus list
            var isFocused = config.UserIds.Any(id =>
                id == userId ||
                id == $"{userId}@g{groupId}" ||
                id == $"{userId}@{groupId}");

            if (!isFocused)
                return;

            _logger.LogInformation("[TavernModule/SpecialFocus] Detected focus user: Group={GroupId}, User={User}",
                groupId, userName);

            // Check cooldown
            var cooldownKey = $"{groupId}:{userId}";
            if (_specialFocusCooldown.TryGetValue(cooldownKey, out var lastTime))
            {
                var elapsed = DateTime.Now - lastTime;
                var cooldown = TimeSpan.FromMinutes(config.CooldownMinutes);
                if (elapsed < cooldown)
                {
                    _logger.LogInformation("[TavernModule/SpecialFocus] User {User} in cooldown", userName);
                    return;
                }
            }

            // Build prompt
            var scenario = config.ScenarioTemplate
                .Replace("{UserName}", userName)
                .Replace("{Message}", content);

            // Generate AI response
            var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
            var response = await _tavernService.GenerateResponseAsync(scenario, userName);

            var messagePrefix = config.MessagePrefix
                .Replace("{CharacterName}", characterName)
                .Replace("{UserName}", userName);
            var message = messagePrefix + response;

            if (_context != null)
            {
                await _context.SendGroupMessageAsync(groupId, message);
            }

            // Record cooldown
            _specialFocusCooldown[cooldownKey] = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TavernModule/SpecialFocus] Error");
        }
    }
}
