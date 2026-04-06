using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi CLI 执行服务接口
/// </summary>
public interface IKimiExecutionService
{
    /// <summary>
    /// 执行Kimi CLI命令
    /// </summary>
    /// <param name="repoPath">仓库工作目录</param>
    /// <param name="arguments">CLI参数</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌（用于外部中断任务）</param>
    /// <returns>执行结果</returns>
    Task<ExecutionResult> ExecuteAsync(string repoPath, string arguments, int timeoutSeconds, CancellationToken cancellationToken = default);
}
