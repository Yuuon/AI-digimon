using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi 执行服务接口 - 通过 ACP 协议与 Kimi 服务交互
/// </summary>
public interface IKimiExecutionService
{
    /// <summary>
    /// 执行Kimi聊天请求（通过 kimi ACP 协议）
    /// </summary>
    /// <param name="workDir">工作目录</param>
    /// <param name="message">用户消息</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌（用于外部中断任务）</param>
    /// <returns>执行结果</returns>
    Task<ExecutionResult> ExecuteAsync(string workDir, string message, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行Kimi聊天请求，支持指定会话ID
    /// </summary>
    /// <param name="workDir">工作目录</param>
    /// <param name="message">用户消息</param>
    /// <param name="sessionId">会话ID（为空则自动创建新会话）</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<ExecutionResult> ExecuteAsync(string workDir, string message, string? sessionId, int timeoutSeconds, CancellationToken cancellationToken = default);
}
