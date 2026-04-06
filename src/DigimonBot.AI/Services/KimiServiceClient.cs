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

            _logger.LogInformation("[KimiService] 正在连接 Kimi ACP 服务...");

            // Dispose old client if exists
            _client?.Dispose();

            _client = new KimiAcpClient(
                kimiExecutablePath: string.IsNullOrEmpty(_options.KimiExecutablePath)
                    ? null
                    : _options.KimiExecutablePath);

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
        try
        {
            var client = _client!;
            var effectiveWorkDir = workDir ?? _options.DefaultWorkDir ?? ".";

            // 创建或恢复会话
            string activeSessionId;
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
            void HandleUpdate(object? sender, SessionUpdateEventArgs e)
            {
                if (e.SessionId != activeSessionId) return;
                if (e.UpdateType == "agent_message_chunk" && e.Content != null)
                {
                    messageBuilder.Append(e.Content);
                }
            }

            client.OnSessionUpdate += HandleUpdate;
            try
            {
                _logger.LogInformation("[KimiService] 发送 ACP 聊天请求: {Message}",
                    message.Length > 100 ? message[..100] + "..." : message);

                var result = await client.SendPromptAsync(activeSessionId, message, ct);

                _logger.LogInformation("[KimiService] ACP 聊天完成, StopReason: {StopReason}", result.StopReason);

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
            throw new KimiServiceException(
                $"Kimi ACP 错误: {ex.Message}",
                ex.ErrorCode,
                ex.Message);
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
