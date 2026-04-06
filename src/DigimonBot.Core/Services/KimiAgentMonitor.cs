namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi Agent 状态监控器 - 使用原子操作保证线程安全
/// </summary>
public class KimiAgentMonitor : IKimiAgentMonitor
{
    // 0 = 空闲, 1 = 忙碌
    private int _busy;
    private CancellationTokenSource? _cts;
    private KimiTaskInfo? _currentTask;

    /// <inheritdoc/>
    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    /// <inheritdoc/>
    public KimiTaskInfo? CurrentTask => _currentTask;

    /// <inheritdoc/>
    public bool TryBeginTask(string userId, string command, out CancellationToken cancellationToken)
    {
        // 原子地从 0 → 1，若已为 1 则失败
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            cancellationToken = CancellationToken.None;
            return false;
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        _currentTask = new KimiTaskInfo
        {
            UserId = userId,
            Command = command.Length > 128 ? command[..128] : command,
            StartedAt = DateTime.UtcNow
        };

        cancellationToken = cts.Token;
        return true;
    }

    /// <inheritdoc/>
    public void EndTask()
    {
        _currentTask = null;

        var cts = Interlocked.Exchange(ref _cts, null);
        try { cts?.Dispose(); }
        catch { /* 忽略Dispose异常 */ }

        Volatile.Write(ref _busy, 0);
    }

    /// <inheritdoc/>
    public bool TryCancel()
    {
        var cts = Volatile.Read(ref _cts);
        if (cts == null)
            return false;

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
