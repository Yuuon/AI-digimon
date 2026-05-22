using DigimonBot.Core.Modules;

namespace DigimonBot.Host.Modules;

/// <summary>
/// Implementation of IModuleContext that bridges modules to the BotService send functionality
/// and provides access to the DI container.
/// </summary>
public class BotModuleContext : IModuleContext
{
    private readonly Func<long, string, Task> _sendGroupMessage;
    private readonly Func<long, string, Task> _sendPrivateMessage;
    private readonly IServiceProvider _serviceProvider;

    public BotModuleContext(
        Func<long, string, Task> sendGroupMessage,
        Func<long, string, Task> sendPrivateMessage,
        IServiceProvider serviceProvider)
    {
        _sendGroupMessage = sendGroupMessage;
        _sendPrivateMessage = sendPrivateMessage;
        _serviceProvider = serviceProvider;
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
