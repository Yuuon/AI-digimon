using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi ACP (Agent Client Protocol) 客户端
/// 通过 JSON-RPC over stdio 与 kimi acp 服务通信
/// 
/// 协议说明:
/// - 请求格式: {"jsonrpc":"2.0","id":1,"method":"method/name","params":{}}
/// - 响应格式: {"jsonrpc":"2.0","id":1,"result":{}} 或 {"jsonrpc":"2.0","id":1,"error":{}}
/// - 通知格式: {"jsonrpc":"2.0","method":"method/name","params":{}} (无 id)
/// </summary>
public class KimiAcpClient : IDisposable
{
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private readonly ILogger? _logger;
    private readonly string _kimiExecutablePath;
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly CancellationTokenSource _cts = new();
    private int _nextId = 1;
    private bool _isInitialized = false;
    private readonly object _lock = new();

    // 事件：会话更新通知（流式输出）
    public event EventHandler<SessionUpdateEventArgs>? OnSessionUpdate;
    public event EventHandler<string>? OnError;
    public event EventHandler? OnDisconnected;

    public bool IsConnected => _process?.HasExited == false;
    public bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// 是否自动批准工具调用（YOLO 模式）
    /// </summary>
    public bool AutoApproveTools { get; set; } = true;

    public KimiAcpClient(ILogger? logger = null, string? kimiExecutablePath = null, bool autoApproveTools = true)
    {
        _logger = logger;
        _kimiExecutablePath = ResolveKimiExecutablePath(kimiExecutablePath);
        AutoApproveTools = autoApproveTools;
    }

    #region 连接管理

    /// <summary>
    /// 启动并连接到 kimi acp 服务
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_process?.HasExited == false)
        {
            _logger?.LogWarning("[KimiAcp] 已经连接到 ACP 服务");
            return;
        }

        _logger?.LogInformation("[KimiAcp] 正在启动 ACP 服务, 可执行文件路径: {Path}", _kimiExecutablePath);

        var psi = new ProcessStartInfo
        {
            FileName = _kimiExecutablePath,
            Arguments = "acp",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi };
        _process.Start();

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
        _stderr = _process.StandardError;

        // 启动后台读取任务
        _ = Task.Run(() => ReadOutputLoopAsync(_cts.Token));
        _ = Task.Run(() => ReadErrorLoopAsync(_cts.Token));

        // 等待进程启动
        await Task.Delay(500, ct);

        if (_process.HasExited)
        {
            throw new InvalidOperationException("ACP 服务启动失败");
        }

        _logger?.LogInformation("[KimiAcp] ACP 服务已启动");
    }

    /// <summary>
    /// 断开连接并清理资源
    /// </summary>
    public void Disconnect()
    {
        _cts.Cancel();
        
        try
        {
            _stdin?.Close();
        }
        catch { }

        if (_process?.HasExited == false)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch { }
        }

        _process?.Dispose();
        _process = null;
        _isInitialized = false;
        
        OnDisconnected?.Invoke(this, EventArgs.Empty);
        _logger?.LogInformation("[KimiAcp] ACP 服务已停止");
    }

    private async Task ReadOutputLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stdout != null)
            {
                var line = await _stdout.ReadLineAsync(ct);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                _logger?.LogDebug("[KimiAcp] <- {Line}", line);

                try
                {
                    var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // 检查是响应、服务端请求还是通知
                    if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
                    {
                        var id = idElement.GetInt32();

                        if (root.TryGetProperty("method", out var reqMethodElement))
                        {
                            // 服务端请求（同时有 id 和 method）
                            var reqMethod = reqMethodElement.GetString();
                            _logger?.LogDebug("[KimiAcp] 收到服务端请求: {Method}, id={Id}", reqMethod, id);

                            if (reqMethod == "session/request_permission" && root.TryGetProperty("params", out var permParams))
                            {
                                // 处理工具调用权限请求 - 自动批准
                                HandlePermissionRequest(id, permParams.Clone());
                            }
                            else
                            {
                                _logger?.LogWarning("[KimiAcp] 收到未处理的服务端请求: {Method}, id={Id}", reqMethod, id);
                            }
                        }
                        else
                        {
                            // 这是响应（有 id，无 method）
                            lock (_lock)
                            {
                                if (_pendingRequests.TryGetValue(id, out var tcs))
                                {
                                    if (root.TryGetProperty("result", out var result))
                                    {
                                        _logger?.LogInformation("[KimiAcp] <- 收到响应 id={Id} (result)", id);
                                        tcs.TrySetResult(result.Clone());
                                    }
                                    else if (root.TryGetProperty("error", out var error))
                                    {
                                        var errorMsg = error.GetProperty("message").GetString() ?? "Unknown error";
                                        var errorCode = error.TryGetProperty("code", out var code) ? code.GetInt32() : 0;
                                        _logger?.LogWarning("[KimiAcp] <- 收到错误响应 id={Id}: [{Code}] {Msg}", id, errorCode, errorMsg);
                                        tcs.TrySetException(new KimiAcpException(errorMsg, errorCode));
                                    }
                                    _pendingRequests.Remove(id);
                                }
                            }
                        }
                    }
                    else if (root.TryGetProperty("method", out var methodElement))
                    {
                        // 这是通知（有 method，无 id）
                        var method = methodElement.GetString();
                        _logger?.LogDebug("[KimiAcp] 收到通知: {Method}", method);
                        
                        if (method == "session/update" && root.TryGetProperty("params", out var paramsElement))
                        {
                            HandleSessionUpdate(paramsElement.Clone());
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "[KimiAcp] 无法解析 JSON: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[KimiAcp] 读取输出时出错");
        }
    }

    private async Task ReadErrorLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stderr != null)
            {
                var line = await _stderr.ReadLineAsync(ct);
                if (line == null) break;
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger?.LogWarning("[KimiAcp] stderr: {Line}", line);
                    OnError?.Invoke(this, line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
    }

    private void HandleSessionUpdate(JsonElement paramsElement)
    {
        try
        {
            var sessionId = paramsElement.GetProperty("sessionId").GetString() ?? "";
            var update = paramsElement.GetProperty("update");
            var updateType = update.GetProperty("sessionUpdate").GetString() ?? "";

            string? content = null;
            if (update.TryGetProperty("content", out var contentElement) && 
                contentElement.TryGetProperty("text", out var textElement))
            {
                content = textElement.GetString();
            }

            // Extract tool-call information when available (helps track what the AI agent is doing)
            string? toolName = null;
            if (update.TryGetProperty("toolName", out var toolNameElement))
            {
                toolName = toolNameElement.GetString();
            }

            var shortSessionId = sessionId.Length > SessionIdLogLength ? sessionId[..SessionIdLogLength] : sessionId;

            // Log tool-call related updates at Information level for better debugging
            if (toolName != null)
            {
                _logger?.LogInformation("[KimiAcp] session/update [{UpdateType}] tool={ToolName} sess={SessionId}",
                    updateType, toolName, shortSessionId);
            }
            else if (content != null)
            {
                var preview = content.Length > SessionUpdatePreviewLength ? content[..SessionUpdatePreviewLength] + "..." : content;
                _logger?.LogInformation("[KimiAcp] session/update [{UpdateType}] sess={SessionId}: {Preview}",
                    updateType, shortSessionId, preview);
            }
            else
            {
                // Non-content, non-tool updates logged at Debug to reduce noise
                _logger?.LogDebug("[KimiAcp] session/update [{UpdateType}] sess={SessionId}",
                    updateType, shortSessionId);
            }

            OnSessionUpdate?.Invoke(this, new SessionUpdateEventArgs
            {
                SessionId = sessionId,
                UpdateType = updateType,
                Content = content
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[KimiAcp] 处理 session/update 失败");
        }
    }

    /// <summary>
    /// 处理工具调用权限请求 - 自动批准（YOLO 模式）
    /// </summary>
    private void HandlePermissionRequest(int requestId, JsonElement paramsElement)
    {
        try
        {
            if (!AutoApproveTools)
            {
                _logger?.LogWarning("[KimiAcp] 自动批准已禁用，无法执行工具调用");
                return;
            }
            
            var sessionId = paramsElement.GetProperty("sessionId").GetString() ?? "";
            var toolCall = paramsElement.GetProperty("toolCall");
            var toolCallId = toolCall.GetProperty("toolCallId").GetString() ?? "";
            var options = paramsElement.GetProperty("options");
            
            // 查找 "approve_for_session" 或 "approve" 选项
            string? selectedOptionId = null;
            foreach (var option in options.EnumerateArray())
            {
                var kind = option.GetProperty("kind").GetString();
                if (kind == "allow_always" || kind == "allow_once")
                {
                    selectedOptionId = option.GetProperty("optionId").GetString();
                    break;
                }
            }

            if (selectedOptionId == null)
            {
                _logger?.LogWarning("[KimiAcp] 未找到批准选项，无法处理权限请求");
                return;
            }

            var shortSessionId = sessionId.Length > SessionIdLogLength ? sessionId[..SessionIdLogLength] : sessionId;
            _logger?.LogInformation("[KimiAcp] 自动批准工具调用: {ToolCallId}, 选项: {Option}, 会话: {SessionId}", 
                toolCallId, selectedOptionId, shortSessionId);

            // 发送权限响应（JSON-RPC 响应格式，必须包含请求的 id）
            _ = Task.Run(async () =>
            {
                try
                {
                    // 注意：这里必须发送 JSON-RPC 响应（带 id 和 result），不是通知
                    var response = new Dictionary<string, object>
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = requestId,
                        ["result"] = new Dictionary<string, object>
                        {
                            ["outcome"] = new Dictionary<string, string>
                            {
                                ["outcome"] = "selected",
                                ["optionId"] = selectedOptionId
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(response, JsonOptions);
                    _logger?.LogDebug("[KimiAcp] -> 发送权限响应: {Json}", json);

                    await _stdin!.WriteLineAsync(json);
                    await _stdin.FlushAsync();
                    
                    _logger?.LogInformation("[KimiAcp] 权限响应已发送: id={Id}", requestId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[KimiAcp] 发送权限响应失败");
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[KimiAcp] 处理权限请求失败");
        }
    }

    private async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct)
    {
        if (_process?.HasExited != false)
            throw new InvalidOperationException("ACP 服务未连接");

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>();

        lock (_lock)
        {
            _pendingRequests[id] = tcs;
        }

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = id,
            Method = method,
            Params = parameters
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        _logger?.LogInformation("[KimiAcp] -> 发送请求 [{Method}] id={Id}", method, id);
        _logger?.LogDebug("[KimiAcp] -> {Json}", json);

        await _stdin!.WriteLineAsync(json);
        await _stdin.FlushAsync();

        try
        {
            // 使用调用方传入的 CancellationToken 控制超时和取消
            return await tcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                _pendingRequests.Remove(id);
            }
            throw;
        }
    }

    #endregion

    #region ACP 方法

    /// <summary>
    /// 初始化连接
    /// </summary>
    public async Task<InitializeResponse> InitializeAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("initialize", new InitializeParams
        {
            ProtocolVersion = 1,
            Capabilities = new(),
            ClientInfo = new Implementation { Name = "DigimonBot", Version = "1.0" }
        }, ct);

        _isInitialized = true;
        return JsonSerializer.Deserialize<InitializeResponse>(result.GetRawText(), JsonOptions)!;
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    public async Task<NewSessionResponse> CreateSessionAsync(string cwd, CancellationToken ct = default)
    {
        var result = await SendRequestAsync("session/new", new NewSessionParams
        {
            Cwd = cwd,
            McpServers = new List<object>()
        }, ct);

        return JsonSerializer.Deserialize<NewSessionResponse>(result.GetRawText(), JsonOptions)!;
    }

    /// <summary>
    /// 列出会话
    /// </summary>
    public async Task<ListSessionsResponse> ListSessionsAsync(string? cwd = null, CancellationToken ct = default)
    {
        var result = await SendRequestAsync("session/list", new ListSessionsParams
        {
            Cwd = cwd
        }, ct);

        return JsonSerializer.Deserialize<ListSessionsResponse>(result.GetRawText(), JsonOptions)!;
    }

    /// <summary>
    /// 加载已有会话
    /// </summary>
    public async Task LoadSessionAsync(string cwd, string sessionId, CancellationToken ct = default)
    {
        await SendRequestAsync("session/load", new LoadSessionParams
        {
            Cwd = cwd,
            SessionId = sessionId,
            McpServers = new List<object>()
        }, ct);
    }

    /// <summary>
    /// 恢复会话
    /// </summary>
    public async Task<ResumeSessionResponse> ResumeSessionAsync(string cwd, string sessionId, CancellationToken ct = default)
    {
        var result = await SendRequestAsync("session/resume", new ResumeSessionParams
        {
            Cwd = cwd,
            SessionId = sessionId,
            McpServers = new List<object>()
        }, ct);

        return JsonSerializer.Deserialize<ResumeSessionResponse>(result.GetRawText(), JsonOptions)!;
    }

    /// <summary>
    /// 发送消息（AI 聊天）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="prompt">消息内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>提示响应（包含 stopReason）</returns>
    public async Task<PromptResponse> SendPromptAsync(string sessionId, string prompt, CancellationToken ct = default)
    {
        var result = await SendRequestAsync("session/prompt", new PromptParams
        {
            SessionId = sessionId,
            Prompt = new List<ContentBlock>
            {
                new() { Type = "text", Text = prompt }
            }
        }, ct);

        return JsonSerializer.Deserialize<PromptResponse>(result.GetRawText(), JsonOptions)!;
    }

    /// <summary>
    /// 取消当前操作
    /// </summary>
    public async Task CancelAsync(string sessionId, CancellationToken ct = default)
    {
        await SendRequestAsync("session/cancel", new { sessionId }, ct);
    }

    #endregion

    #region 工具方法

    private const int SessionIdLogLength = 8;
    private const int SessionUpdatePreviewLength = 80;

    /// <summary>
    /// 解析 kimi 可执行文件路径，将相对名称（如 "kimi"）解析为绝对路径。
    /// 当进程以服务方式运行时，PATH 可能不包含 kimi 的安装目录，
    /// 因此需要主动搜索常见安装位置。
    /// </summary>
    private static string ResolveKimiExecutablePath(string? providedPath)
    {
        // Determine the executable name to search for
        var executableName = string.IsNullOrWhiteSpace(providedPath) ? "kimi" : providedPath;

        // If an absolute path was provided and the file exists, use it directly
        if (Path.IsPathRooted(executableName) && File.Exists(executableName))
            return executableName;

        // If a relative path was provided and it resolves to an existing file, use the full path
        if (executableName.Contains(Path.DirectorySeparatorChar) || executableName.Contains('/'))
        {
            var fullRelative = Path.GetFullPath(executableName);
            if (File.Exists(fullRelative))
                return fullRelative;
        }

        // Extract just the filename for searching well-known locations and PATH
        var fileName = Path.GetFileName(executableName);

        // Well-known installation locations (checked first since service PATH may be minimal)
        var wellKnownPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", fileName),
            $"/usr/local/bin/{fileName}",
            $"/usr/bin/{fileName}"
        };

        foreach (var candidate in wellKnownPaths)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Search PATH environment variable directories
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;

                var fullPath = Path.Combine(dir, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        // Fallback: return the provided name as-is and let Process.Start handle it
        return executableName;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public void Dispose()
    {
        Disconnect();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

#region 数据模型

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

public class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public Dictionary<string, object> Capabilities { get; set; } = new();

    [JsonPropertyName("clientInfo")]
    public Implementation ClientInfo { get; set; } = new();
}

public class Implementation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public class InitializeResponse
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("agentCapabilities")]
    public AgentCapabilities AgentCapabilities { get; set; } = new();

    [JsonPropertyName("agentInfo")]
    public Implementation AgentInfo { get; set; } = new();

    [JsonPropertyName("authMethods")]
    public List<AuthMethod> AuthMethods { get; set; } = new();
}

public class AgentCapabilities
{
    [JsonPropertyName("loadSession")]
    public bool LoadSession { get; set; }

    [JsonPropertyName("promptCapabilities")]
    public PromptCapabilities PromptCapabilities { get; set; } = new();

    [JsonPropertyName("mcpCapabilities")]
    public McpCapabilities McpCapabilities { get; set; } = new();

    [JsonPropertyName("sessionCapabilities")]
    public SessionCapabilities SessionCapabilities { get; set; } = new();
}

public class PromptCapabilities
{
    [JsonPropertyName("embeddedContext")]
    public bool EmbeddedContext { get; set; }

    [JsonPropertyName("image")]
    public bool Image { get; set; }

    [JsonPropertyName("audio")]
    public bool Audio { get; set; }
}

public class McpCapabilities
{
    [JsonPropertyName("http")]
    public bool Http { get; set; }

    [JsonPropertyName("sse")]
    public bool Sse { get; set; }
}

public class SessionCapabilities
{
    [JsonPropertyName("list")]
    public object List { get; set; } = new();

    [JsonPropertyName("resume")]
    public object Resume { get; set; } = new();
}

public class AuthMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class NewSessionParams
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; set; } = string.Empty;

    [JsonPropertyName("mcpServers")]
    public List<object> McpServers { get; set; } = new();
}

public class NewSessionResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("modes")]
    public SessionModeState Modes { get; set; } = new();

    [JsonPropertyName("models")]
    public SessionModelState Models { get; set; } = new();
}

public class SessionModeState
{
    [JsonPropertyName("availableModes")]
    public List<SessionMode> AvailableModes { get; set; } = new();

    [JsonPropertyName("currentModeId")]
    public string CurrentModeId { get; set; } = string.Empty;
}

public class SessionMode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class SessionModelState
{
    [JsonPropertyName("availableModels")]
    public List<ModelInfo> AvailableModels { get; set; } = new();

    [JsonPropertyName("currentModelId")]
    public string CurrentModelId { get; set; } = string.Empty;
}

public class ModelInfo
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class ListSessionsParams
{
    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

public class ListSessionsResponse
{
    [JsonPropertyName("sessions")]
    public List<SessionInfo> Sessions { get; set; } = new();

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

public class SessionInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("cwd")]
    public string Cwd { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public class LoadSessionParams
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("mcpServers")]
    public List<object> McpServers { get; set; } = new();
}

public class ResumeSessionParams : LoadSessionParams { }

public class ResumeSessionResponse
{
    [JsonPropertyName("modes")]
    public SessionModeState Modes { get; set; } = new();

    [JsonPropertyName("models")]
    public SessionModelState Models { get; set; } = new();
}

public class PromptParams
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public List<ContentBlock> Prompt { get; set; } = new();
}

public class ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class PromptResponse
{
    [JsonPropertyName("stopReason")]
    public string StopReason { get; set; } = string.Empty;
}

public class SessionUpdateEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string UpdateType { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public class KimiAcpException : Exception
{
    public int ErrorCode { get; }

    public KimiAcpException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}

#endregion
