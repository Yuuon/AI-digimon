namespace DigimonBot.Core.Modules;

/// <summary>
/// Base interface for all hot-reloadable modules.
/// Each module handles specific functionality and can be loaded/unloaded at runtime.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Unique module name used for identification and reload targeting.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Module description for help/listing.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initialize the module. Called when the module is first loaded or reloaded.
    /// </summary>
    Task InitializeAsync(IModuleContext context);

    /// <summary>
    /// Shutdown the module gracefully. Called before unload or reload.
    /// </summary>
    Task ShutdownAsync();

    /// <summary>
    /// Handle an incoming message. Return true if the message was consumed by this module.
    /// </summary>
    Task<ModuleResult> HandleMessageAsync(ModuleMessage message);

    /// <summary>
    /// Handle a raw event (non-message events like heartbeat, notice, etc.)
    /// </summary>
    Task HandleEventAsync(ModuleEvent evt);
}

/// <summary>
/// Context provided to modules during initialization, giving access to shared services.
/// </summary>
public interface IModuleContext
{
    /// <summary>
    /// Send a message to a group.
    /// </summary>
    Task SendGroupMessageAsync(long groupId, string message);

    /// <summary>
    /// Send a message to a private user.
    /// </summary>
    Task SendPrivateMessageAsync(long userId, string message);

    /// <summary>
    /// Get a registered service by type.
    /// </summary>
    T? GetService<T>() where T : class;

    /// <summary>
    /// Get a required registered service by type.
    /// </summary>
    T GetRequiredService<T>() where T : class;
}

/// <summary>
/// Incoming message passed to modules for processing.
/// </summary>
public class ModuleMessage
{
    /// <summary>User ID (may include group isolation prefix)</summary>
    public string UserId { get; set; } = "";
    /// <summary>Original user ID (pure QQ number)</summary>
    public string OriginalUserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Content { get; set; } = "";
    public long? GroupId { get; set; }
    public bool IsGroupMessage { get; set; }
    public bool IsMentioned { get; set; }
    public DateTime Timestamp { get; set; }
    public List<string> MentionedUserIds { get; set; } = new();
    /// <summary>Raw JSON message data for advanced parsing</summary>
    public object? RawMessage { get; set; }
}

/// <summary>
/// Raw event (heartbeat, notice, etc.) passed to modules.
/// </summary>
public class ModuleEvent
{
    public string PostType { get; set; } = "";
    public string? EventType { get; set; }
    public string? RawJson { get; set; }
}

/// <summary>
/// Result of module message handling.
/// </summary>
public class ModuleResult
{
    /// <summary>Whether the module handled/consumed the message.</summary>
    public bool Handled { get; set; }
    /// <summary>Response text to send back.</summary>
    public string? Response { get; set; }
    /// <summary>Additional message parts to send.</summary>
    public List<string> AdditionalMessages { get; set; } = new();
    /// <summary>Whether an evolution occurred during handling.</summary>
    public bool EvolutionOccurred { get; set; }
    /// <summary>Evolution notification message.</summary>
    public string? EvolutionMessage { get; set; }

    public static ModuleResult NotHandled() => new() { Handled = false };
    public static ModuleResult Success(string response) => new() { Handled = true, Response = response };
}
