using System.Diagnostics;
using System.Text;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi ACP 执行服务 - 通过 ACP (Agent Client Protocol) 与 Kimi 通信
/// 支持自动批准工具调用（YOLO 模式）
/// </summary>
public class KimiAcpExecutionService : IKimiExecutionService
{
    private readonly ILogger<KimiAcpExecutionService> _logger;
    private readonly string _kimiCliPath;
    private readonly string _basePath;
    private readonly bool _autoApproveTools;
    
    // 每个工作目录对应一个 ACP 会话
    private readonly Dictionary<string, KimiAcpSession> _sessions = new();
    private readonly object _lock = new();

    public KimiAcpExecutionService(
        ILogger<KimiAcpExecutionService> logger,
        string kimiCliPath,
        string basePath,
        bool autoApproveTools = true)
    {
        _logger = logger;
        _kimiCliPath = kimiCliPath;
        _basePath = basePath;
        _autoApproveTools = autoApproveTools;
    }

    public async Task<ExecutionResult> ExecuteAsync(string workDir, string message, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(workDir, message, null, timeoutSeconds, cancellationToken);
    }

    public async Task<ExecutionResult> ExecuteAsync(
        string workDir, 
        string message, 
        string? sessionId, 
        int timeoutSeconds, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "[KimiAcpExec] 开始执行: 工作目录={WorkDir}, 会话={Session}, 消息长度={MsgLen}",
            workDir, sessionId ?? "(新会话)", message.Length);

        KimiAcpSession? session = null;
        
        try
        {
            // 获取或创建会话
            session = await GetOrCreateSessionAsync(workDir, sessionId, cancellationToken);
            
            // 收集流式输出
            var outputBuilder = new StringBuilder();
            session.OnMessageReceived += (s, chunk) => outputBuilder.Append(chunk);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // 发送消息
            await session.ChatAsync(message, linkedCts.Token);

            stopwatch.Stop();

            var result = new ExecutionResult
            {
                Success = true,
                Output = outputBuilder.ToString(),
                SessionId = session.SessionId,
                ExitCode = 0,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            _logger.LogInformation(
                "[KimiAcpExec] 执行完成 - 会话: {Session}, 耗时: {Duration}ms, 输出长度: {Len}",
                result.SessionId, result.DurationMs, result.Output.Length);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            
            // 尝试取消
            if (session != null)
            {
                try { await session.CancelAsync(); } catch { }
            }

            bool isExternalCancel = cancellationToken.IsCancellationRequested;
            if (isExternalCancel)
            {
                _logger.LogInformation("[KimiAcpExec] 任务已被用户取消");
                return new ExecutionResult
                {
                    Success = false,
                    Output = "",
                    Error = "任务已被用户中断。",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            else
            {
                _logger.LogWarning("[KimiAcpExec] 执行超时 ({Timeout}s)", timeoutSeconds);
                return new ExecutionResult
                {
                    Success = false,
                    Output = "",
                    Error = $"执行超时（{timeoutSeconds}秒）。请求已被取消。",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[KimiAcpExec] 执行异常");

            return new ExecutionResult
            {
                Success = false,
                Output = "",
                Error = $"执行异常: {ex.Message}",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// 获取或创建 ACP 会话
    /// </summary>
    private async Task<KimiAcpSession> GetOrCreateSessionAsync(
        string workDir, 
        string? sessionId, 
        CancellationToken ct)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(workDir, out var existingSession) && existingSession.IsConnected)
            {
                _logger.LogDebug("[KimiAcpExec] 复用已有会话: {WorkDir}", workDir);
                return existingSession;
            }
        }

        _logger.LogInformation("[KimiAcpExec] 创建新 ACP 会话: {WorkDir}", workDir);

        var session = new KimiAcpSession(workDir, _logger, _kimiCliPath, _autoApproveTools);
        
        try
        {
            await session.ConnectAsync(ct);
            
            if (sessionId != null)
            {
                try
                {
                    await session.ResumeSessionAsync(sessionId, ct);
                    _logger.LogInformation("[KimiAcpExec] 恢复会话成功: {SessionId}", sessionId);
                }
                catch
                {
                    _logger.LogWarning("[KimiAcpExec] 恢复会话失败，创建新会话: {SessionId}", sessionId);
                    await session.CreateSessionAsync(ct);
                }
            }
            else
            {
                await session.CreateSessionAsync(ct);
            }

            lock (_lock)
            {
                _sessions[workDir] = session;
            }

            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 断开所有会话
    /// </summary>
    public void DisconnectAll()
    {
        lock (_lock)
        {
            foreach (var session in _sessions.Values)
            {
                try
                {
                    session.Disconnect();
                    session.Dispose();
                }
                catch { }
            }
            _sessions.Clear();
        }
    }
}
