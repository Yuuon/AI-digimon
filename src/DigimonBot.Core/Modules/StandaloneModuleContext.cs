using Microsoft.Extensions.DependencyInjection;

namespace DigimonBot.Core.Modules;

/// <summary>
/// A standalone module context for running modules independently outside the main bot.
/// Useful for testing, standalone tools, or running a single module in isolation.
/// 
/// Usage example:
/// <code>
/// var services = new ServiceCollection();
/// // Register needed services...
/// var provider = services.BuildServiceProvider();
/// 
/// var context = new StandaloneModuleContext(
///     sendGroup: async (gid, msg) => Console.WriteLine($"[Group {gid}] {msg}"),
///     sendPrivate: async (uid, msg) => Console.WriteLine($"[Private {uid}] {msg}"),
///     serviceProvider: provider);
///
/// var module = new TavernModule(...);
/// await module.InitializeAsync(context);
/// var result = await module.HandleMessageAsync(message);
/// </code>
/// </summary>
public class StandaloneModuleContext : IModuleContext
{
    private readonly Func<long, string, Task> _sendGroupMessage;
    private readonly Func<long, string, Task> _sendPrivateMessage;
    private readonly IServiceProvider _serviceProvider;

    public StandaloneModuleContext(
        Func<long, string, Task> sendGroup,
        Func<long, string, Task> sendPrivate,
        IServiceProvider serviceProvider)
    {
        _sendGroupMessage = sendGroup;
        _sendPrivateMessage = sendPrivate;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Create a context with no-op message senders (useful for testing).
    /// </summary>
    public static StandaloneModuleContext CreateNoOp(IServiceProvider serviceProvider)
    {
        return new StandaloneModuleContext(
            sendGroup: (_, _) => Task.CompletedTask,
            sendPrivate: (_, _) => Task.CompletedTask,
            serviceProvider: serviceProvider);
    }

    /// <summary>
    /// Create a context that logs messages to console.
    /// </summary>
    public static StandaloneModuleContext CreateConsole(IServiceProvider serviceProvider)
    {
        return new StandaloneModuleContext(
            sendGroup: (gid, msg) =>
            {
                Console.WriteLine($"[Group {gid}] {msg}");
                return Task.CompletedTask;
            },
            sendPrivate: (uid, msg) =>
            {
                Console.WriteLine($"[Private {uid}] {msg}");
                return Task.CompletedTask;
            },
            serviceProvider: serviceProvider);
    }

    public Task SendGroupMessageAsync(long groupId, string message)
        => _sendGroupMessage(groupId, message);

    public Task SendPrivateMessageAsync(long userId, string message)
        => _sendPrivateMessage(userId, message);

    public T? GetService<T>() where T : class
        => _serviceProvider.GetService(typeof(T)) as T;

    public T GetRequiredService<T>() where T : class
        => (T)(_serviceProvider.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered"));
}
