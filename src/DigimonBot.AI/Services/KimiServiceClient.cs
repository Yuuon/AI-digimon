using System.Text;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi ACP 服务客户端 - 通过 JSON-RPC over stdio 与 kimi acp 服务通信
/// 替代原有的 HTTP Web 服务方式，使用 KimiAcpClient 进行底层通信
/// </summary>
public class KimiServiceClient : IKimiServiceClient
{
    private readonly KimiServiceOptions _options;
    private readonly ILogger<KimiServiceClient> _logger;
    private KimiAcpClient? _client;
    private bool _isDisposed;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _chatLock = new(1, 1);

    /// <summary>
    /// ACP 协议中表示 AI 回复正常结束的 stopReason 值
    /// </summary>
    private const string StopReasonEndTurn = "end_turn";
    private const string StopReasonStop = "stop";
    private const int StreamChunkPreviewLength = 60;
    private const int SessionIdLogLength = 8;

    public KimiServiceClient(
        KimiServiceOptions options,
        ILogger<KimiServiceClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    #region 服务生命周期管理

    /// <inheritdoc/>
    public async Task EnsureServiceRunningAsync(CancellationToken ct = default)
    {
        if (_client?.IsConnected == true && _client.IsInitialized)
            return;

        await _connectLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_client?.IsConnected == true && _client.IsInitialized)
                return;

            // Dispose old client if it's in broken state (process exited, not initialized, etc.)
            if (_client != null)
            {
                _logger.LogWarning("[KimiService] ACP 服务不可用 (IsConnected={Connected}, IsInitialized={Init}), 正在重建连接...",
                    _client.IsConnected, _client.IsInitialized);
                try { _client.Dispose(); } catch { }
                _client = null;
            }

            _logger.LogInformation("[KimiService] 正在连接 Kimi ACP 服务...");

            _client = new KimiAcpClient(
                kimiExecutablePath: string.IsNullOrEmpty(_options.KimiExecutablePath)
                    ? null
                    : _options.KimiExecutablePath,
                processKillTimeoutMs: _options.ProcessKillTimeoutMs);

            await _client.ConnectAsync(ct);
            var initResult = await _client.InitializeAsync(ct);

            _logger.LogInformation(
                "[KimiService] ACP 服务已连接: {AgentName} v{AgentVersion}",
                initResult.AgentInfo.Name,
                initResult.AgentInfo.Version);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopServiceAsync(CancellationToken ct = default)
    {
        await _connectLock.WaitAsync(ct);
        try
        {
            if (_client != null)
            {
                _logger.LogInformation("[KimiService] 正在断开 Kimi ACP 服务...");
                _client.Disconnect();
                _client.Dispose();
                _client = null;
                _logger.LogInformation("[KimiService] ACP 服务已断开");
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    #endregion

    #region 聊天 API

    /// <inheritdoc/>
    public async Task<KimiChatResponse> ChatAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        // ACP 协议下同一时间只能处理一个 prompt
        await _chatLock.WaitAsync(ct);

        // Hoist session ID so catch blocks can send session/cancel to the ACP process
        string? activeSessionId = null;
        try
        {
            var client = _client!;
            var effectiveWorkDir = workDir ?? _options.DefaultWorkDir ?? ".";

            // 创建或恢复会话
            if (string.IsNullOrEmpty(sessionId))
            {
                var newSession = await client.CreateSessionAsync(effectiveWorkDir, ct);
                activeSessionId = newSession.SessionId;
                _logger.LogInformation("[KimiService] 创建新 ACP 会话: {SessionId}", activeSessionId);
            }
            else
            {
                try
                {
                    await client.ResumeSessionAsync(effectiveWorkDir, sessionId, ct);
                    activeSessionId = sessionId;
                    _logger.LogInformation("[KimiService] 恢复 ACP 会话: {SessionId}", sessionId);
                }
                catch (KimiAcpException ex)
                {
                    _logger.LogWarning("[KimiService] 恢复会话失败 ({Error}), 创建新会话", ex.Message);
                    var newSession = await client.CreateSessionAsync(effectiveWorkDir, ct);
                    activeSessionId = newSession.SessionId;
                    _logger.LogInformation("[KimiService] 创建新 ACP 会话: {SessionId}", activeSessionId);
                }
            }

            // 收集流式响应
            var messageBuilder = new StringBuilder();
            int chunkCount = 0;
            // Capture for closure — activeSessionId is non-null at this point
            var capturedSessionId = activeSessionId;
            void HandleUpdate(object? sender, SessionUpdateEventArgs e)
            {
                if (e.SessionId != capturedSessionId) return;
                if (e.UpdateType == "agent_message_chunk" && e.Content != null)
                {
                    messageBuilder.Append(e.Content);
                    chunkCount++;
                    var preview = e.Content.Length > StreamChunkPreviewLength ? e.Content[..StreamChunkPreviewLength] + "..." : e.Content;
                    _logger.LogDebug("[KimiService] 流式块 #{Seq} [{UpdateType}]: {Preview}",
                        chunkCount, e.UpdateType, preview);
                }
                else if (e.UpdateType != null)
                {
                    _logger.LogDebug("[KimiService] 收到更新 [{UpdateType}] sess={SessionId}",
                        e.UpdateType, capturedSessionId.Length > SessionIdLogLength ? capturedSessionId[..SessionIdLogLength] : capturedSessionId);
                }
            }

            client.OnSessionUpdate += HandleUpdate;
            try
            {
                _logger.LogInformation("[KimiService] 发送 ACP 聊天请求: {Message}",
                    message.Length > 100 ? message[..100] + "..." : message);

                var result = await client.SendPromptAsync(activeSessionId, message, ct);

                _logger.LogInformation("[KimiService] ACP 聊天完成, StopReason: {StopReason}, 共收到 {ChunkCount} 个流式块, 响应长度: {Len} 字符",
                    result.StopReason, chunkCount, messageBuilder.Length);

                return new KimiChatResponse
                {
                    Response = messageBuilder.ToString(),
                    SessionId = activeSessionId,
                    Completed = result.StopReason == StopReasonEndTurn || result.StopReason == StopReasonStop
                };
            }
            finally
            {
                client.OnSessionUpdate -= HandleUpdate;
            }
        }
        catch (KimiAcpException ex)
        {
            // ACP 协议层错误，可能 ACP 进程已不可用，强制回收以便下次自动重建
            ForceRecycleClient("KimiAcpException: " + ex.Message);
            throw new KimiServiceException(
                $"Kimi ACP 错误: {ex.Message}",
                ex.ErrorCode,
                ex.Message);
        }
        catch (OperationCanceledException)
        {
            // 超时或取消: 先通过 ACP 协议通知 agent 停止当前操作，再回收进程
            await TrySendSessionCancelAsync(activeSessionId);
            ForceRecycleClient("OperationCanceled (超时/取消)");
            throw;
        }
        catch (Exception ex) when (ex is not KimiServiceException)
        {
            // 任何非预期异常后也需要回收，防止 ACP 进程卡死
            await TrySendSessionCancelAsync(activeSessionId);
            ForceRecycleClient(ex.GetType().Name + ": " + ex.Message);
            throw;
        }
        finally
        {
            _chatLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> ChatSimpleAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        var response = await ChatAsync(message, sessionId, workDir, yolo, ct);
        return response.Response;
    }

    #endregion

    #region 会话管理

    /// <inheritdoc/>
    public async Task<KimiSessionInfo> CreateSessionAsync(string? workDir = null, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var effectiveWorkDir = workDir ?? _options.DefaultWorkDir ?? ".";
        var result = await _client!.CreateSessionAsync(effectiveWorkDir, ct);

        var now = DateTime.UtcNow;
        return new KimiSessionInfo
        {
            Id = result.SessionId,
            WorkDir = effectiveWorkDir,
            CreatedAt = now,
            LastActivity = now,
            MessageCount = 0
        };
    }

    /// <inheritdoc/>
    public async Task<List<KimiSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var result = await _client!.ListSessionsAsync(null, ct);
        return result.Sessions.Select(MapSessionInfo).ToList();
    }

    /// <inheritdoc/>
    public async Task<KimiSessionInfo?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var sessions = await ListSessionsAsync(ct);
        return sessions.FirstOrDefault(s =>
            string.Equals(s.Id, sessionId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        // ACP 协议不支持删除会话操作
        _logger.LogWarning("[KimiService] ACP 协议不支持删除会话操作，sessionId: {SessionId}", sessionId);
        await Task.CompletedTask;
        return false;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 强制回收 ACP 客户端连接，确保下次调用自动重建
    /// 用于异常/超时后 ACP 进程可能处于不确定状态的情况
    /// </summary>
    private void ForceRecycleClient(string reason)
    {
        _logger.LogWarning("[KimiService] 强制回收 ACP 连接: {Reason}", reason);
        try
        {
            _client?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[KimiService] 回收时 Dispose 异常 (已忽略)");
        }
        _client = null;
    }

    /// <summary>
    /// 尝试通过 ACP 协议发送 session/cancel 请求，通知 Kimi agent 停止当前操作。
    /// 使用短超时，失败时不抛异常（后续会由 ForceRecycleClient 强制回收进程）。
    /// </summary>
    private async Task TrySendSessionCancelAsync(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || _client?.IsConnected != true)
            return;

        try
        {
            using var cancelCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _client.CancelAsync(sessionId, cancelCts.Token);
            _logger.LogInformation("[KimiService] 已向 ACP 发送 session/cancel (会话: {SessionId})", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[KimiService] 发送 session/cancel 失败 (已忽略，将由 ForceRecycle 回收)");
        }
    }

    /// <summary>
    /// 将 ACP SessionInfo 映射为 KimiSessionInfo
    /// </summary>
    private static KimiSessionInfo MapSessionInfo(SessionInfo session)
    {
        var timestamp = DateTime.TryParse(session.UpdatedAt, out var dt)
            ? dt
            : DateTime.UtcNow;

        return new KimiSessionInfo
        {
            Id = session.SessionId,
            WorkDir = session.Cwd,
            CreatedAt = timestamp,
            LastActivity = timestamp,
            MessageCount = 0 // ACP 协议不提供消息计数
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _connectLock.Dispose();
        _chatLock.Dispose();
        _client?.Dispose();
        _client = null;

        GC.SuppressFinalize(this);
    }

    #endregion
}
