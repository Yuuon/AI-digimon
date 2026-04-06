using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi ACP 会话管理器
/// 封装 KimiAcpClient，提供简单易用的 API
/// </summary>
public class KimiAcpSession : IDisposable
{
    private readonly KimiAcpClient _client;
    private readonly ILogger<KimiAcpSession>? _logger;
    private readonly string _workDir;
    private string? _sessionId;
    private readonly StringBuilder _thoughtBuilder = new();
    private readonly StringBuilder _messageBuilder = new();
    private readonly ConcurrentQueue<SessionUpdate> _updateQueue = new();
    private TaskCompletionSource<bool>? _promptCompletion;
    private bool _isDisposed;

    public string? SessionId => _sessionId;
    public string WorkDir => _workDir;
    public bool IsConnected => _client.IsConnected;

    public event EventHandler<string>? OnThoughtReceived;
    public event EventHandler<string>? OnMessageReceived;
    public event EventHandler? OnPromptCompleted;

    public KimiAcpSession(string workDir, ILogger<KimiAcpSession>? logger = null)
    {
        _workDir = workDir;
        _logger = logger;
        _client = new KimiAcpClient(logger);
        _client.OnSessionUpdate += HandleSessionUpdate;
        _client.OnError += (s, e) => _logger?.LogError("[KimiAcpSession] {Error}", e);
        _client.OnDisconnected += (s, e) => _logger?.LogWarning("[KimiAcpSession] ACP 连接断开");
    }

    #region 连接管理

    /// <summary>
    /// 连接到 ACP 服务并初始化
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _client.ConnectAsync(ct);
        var initResult = await _client.InitializeAsync(ct);
        
        _logger?.LogInformation(
            "[KimiAcpSession] 已连接到 ACP 服务: {AgentName} v{AgentVersion}",
            initResult.AgentInfo.Name,
            initResult.AgentInfo.Version);
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        _client.Disconnect();
    }

    #endregion

    #region 会话管理

    /// <summary>
    /// 创建新会话
    /// </summary>
    public async Task CreateSessionAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        
        var result = await _client.CreateSessionAsync(_workDir, ct);
        _sessionId = result.SessionId;
        
        _logger?.LogInformation(
            "[KimiAcpSession] 创建会话成功: {SessionId}, 模型: {Model}",
            _sessionId,
            result.Models.CurrentModelId);
    }

    /// <summary>
    /// 加载已有会话
    /// </summary>
    public async Task LoadSessionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        
        await _client.LoadSessionAsync(_workDir, sessionId, ct);
        _sessionId = sessionId;
        
        _logger?.LogInformation("[KimiAcpSession] 加载会话: {SessionId}", sessionId);
    }

    /// <summary>
    /// 恢复会话（带模型状态）
    /// </summary>
    public async Task ResumeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        
        var result = await _client.ResumeSessionAsync(_workDir, sessionId, ct);
        _sessionId = sessionId;
        
        _logger?.LogInformation(
            "[KimiAcpSession] 恢复会话: {SessionId}, 当前模型: {Model}",
            sessionId,
            result.Models.CurrentModelId);
    }

    /// <summary>
    /// 列出当前工作目录的会话
    /// </summary>
    public async Task<List<SessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        
        var result = await _client.ListSessionsAsync(_workDir, ct);
        return result.Sessions;
    }

    #endregion

    #region AI 聊天

    /// <summary>
    /// 发送消息并等待完整响应（同步收集所有流式输出）
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>AI 的完整回复</returns>
    public async Task<string> ChatAsync(string message, CancellationToken ct = default)
    {
        EnsureSession();
        
        // 清空之前的输出
        _thoughtBuilder.Clear();
        _messageBuilder.Clear();
        _promptCompletion = new TaskCompletionSource<bool>();

        try
        {
            // 发送 prompt
            var responseTask = _client.SendPromptAsync(_sessionId!, message, ct);
            
            // 等待流式输出完成或超时
            var completedTask = await Task.WhenAny(
                _promptCompletion.Task,
                Task.Delay(TimeSpan.FromMinutes(5), ct)
            );

            if (completedTask != _promptCompletion.Task)
            {
                throw new TimeoutException("AI 响应超时");
            }

            // 等待最终响应
            var result = await responseTask;
            
            _logger?.LogInformation(
                "[KimiAcpSession] Prompt 完成, StopReason: {StopReason}",
                result.StopReason);

            return _messageBuilder.ToString();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 用户取消，尝试发送取消命令
            try
            {
                await _client.CancelAsync(_sessionId!);
            }
            catch { }
            throw;
        }
    }

    /// <summary>
    /// 发送消息并实时接收流式输出（通过事件）
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="onChunk">收到内容片段时的回调</param>
    /// <param name="ct">取消令牌</param>
    public async Task ChatStreamingAsync(
        string message,
        Action<string> onChunk,
        CancellationToken ct = default)
    {
        EnsureSession();

        var chunkReceived = false;
        var completionTcs = new TaskCompletionSource<bool>();

        void HandleChunk(object? sender, string chunk)
        {
            chunkReceived = true;
            onChunk(chunk);
        }

        void HandleComplete(object? sender, EventArgs e)
        {
            completionTcs.TrySetResult(true);
        }

        OnMessageReceived += HandleChunk;
        OnPromptCompleted += HandleComplete;

        try
        {
            // 清空之前的输出
            _messageBuilder.Clear();
            _promptCompletion = completionTcs;

            // 发送 prompt
            var responseTask = _client.SendPromptAsync(_sessionId!, message, ct);

            // 等待完成
            var completedTask = await Task.WhenAny(
                completionTcs.Task,
                Task.Delay(TimeSpan.FromMinutes(5), ct)
            );

            if (completedTask != completionTcs.Task)
            {
                throw new TimeoutException("AI 响应超时");
            }

            // 获取最终结果
            var result = await responseTask;
            
            _logger?.LogInformation(
                "[KimiAcpSession] 流式输出完成, StopReason: {StopReason}",
                result.StopReason);
        }
        finally
        {
            OnMessageReceived -= HandleChunk;
            OnPromptCompleted -= HandleComplete;
        }
    }

    /// <summary>
    /// 取消当前正在进行的操作
    /// </summary>
    public async Task CancelAsync(CancellationToken ct = default)
    {
        if (_sessionId != null)
        {
            await _client.CancelAsync(_sessionId, ct);
        }
    }

    /// <summary>
    /// 获取当前会话的思考过程（如果有）
    /// </summary>
    public string GetThoughtProcess() => _thoughtBuilder.ToString();

    #endregion

    #region 事件处理

    private void HandleSessionUpdate(object? sender, SessionUpdateEventArgs e)
    {
        if (e.SessionId != _sessionId) return;

        switch (e.UpdateType)
        {
            case "agent_thought_chunk":
                if (e.Content != null)
                {
                    _thoughtBuilder.Append(e.Content);
                    OnThoughtReceived?.Invoke(this, e.Content);
                }
                break;

            case "agent_message_chunk":
                if (e.Content != null)
                {
                    _messageBuilder.Append(e.Content);
                    OnMessageReceived?.Invoke(this, e.Content);
                }
                break;

            case "available_commands_update":
                // 忽略命令更新
                break;

            default:
                _logger?.LogDebug("[KimiAcpSession] 未知更新类型: {UpdateType}", e.UpdateType);
                break;
        }
    }

    #endregion

    #region 工具方法

    private void EnsureConnected()
    {
        if (!_client.IsConnected)
            throw new InvalidOperationException("ACP 服务未连接，请先调用 ConnectAsync()");
    }

    private void EnsureSession()
    {
        EnsureConnected();
        if (_sessionId == null)
            throw new InvalidOperationException("没有活动会话，请先调用 CreateSessionAsync() 或 LoadSessionAsync()");
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// 会话更新记录
/// </summary>
public class SessionUpdate
{
    public string UpdateType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
