namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi Agent 状态监控器接口 - 负责聊天入口层的并发控制
/// （仅控制来自聊天频道的请求，不干预内部子Agent操作）
/// </summary>
public interface IKimiAgentMonitor
{
    /// <summary>
    /// 当前是否有任务正在执行
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// 当前正在执行的任务信息（若无任务则为 null）
    /// </summary>
    KimiTaskInfo? CurrentTask { get; }

    /// <summary>
    /// 尝试开始新任务。若已有任务在执行则返回 false。
    /// 成功时将状态设为 Busy 并返回可用于取消的 CancellationToken。
    /// </summary>
    bool TryBeginTask(string userId, string command, out CancellationToken cancellationToken);

    /// <summary>
    /// 结束当前任务，将状态重置为空闲。
    /// 应在 try/finally 块中调用以保证状态正确释放。
    /// </summary>
    void EndTask();

    /// <summary>
    /// 尝试取消当前正在执行的任务。
    /// </summary>
    /// <returns>若有任务且取消信号已发送则返回 true，否则返回 false</returns>
    bool TryCancel();
}

/// <summary>
/// 当前 Kimi 任务的元数据
/// </summary>
public class KimiTaskInfo
{
    /// <summary>发起任务的用户ID</summary>
    public string UserId { get; set; } = "";

    /// <summary>任务命令内容（前128字符）</summary>
    public string Command { get; set; } = "";

    /// <summary>任务开始时间（UTC）</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>任务已运行时长</summary>
    public TimeSpan Elapsed => DateTime.UtcNow - StartedAt;
}
