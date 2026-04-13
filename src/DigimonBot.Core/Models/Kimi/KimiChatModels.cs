using System.Text.Json.Serialization;

namespace DigimonBot.Core.Models.Kimi;

/// <summary>
/// Kimi Web 服务聊天请求
/// </summary>
public class KimiChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("yolo")]
    public bool Yolo { get; set; } = true;

    [JsonPropertyName("work_dir")]
    public string? WorkDir { get; set; }
}

/// <summary>
/// Kimi Web 服务聊天响应
/// </summary>
public class KimiChatResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<KimiToolCallInfo>? ToolCalls { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
}

/// <summary>
/// Kimi 工具调用信息
/// </summary>
public class KimiToolCallInfo
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}

/// <summary>
/// Kimi 会话信息
/// </summary>
public class KimiSessionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("work_dir")]
    public string WorkDir { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("last_activity")]
    public DateTime LastActivity { get; set; }

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }
}

/// <summary>
/// Kimi Web 服务配置选项
/// </summary>
public class KimiServiceOptions
{
    /// <summary>
    /// 请求超时时间（秒），默认 5 分钟
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Kimi CLI 可执行文件路径
    /// </summary>
    public string KimiExecutablePath { get; set; } = "kimi";

    /// <summary>
    /// 默认工作目录
    /// </summary>
    public string? DefaultWorkDir { get; set; }

    /// <summary>
    /// ACP 进程终止等待超时（毫秒）
    /// </summary>
    public int ProcessKillTimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Kimi 服务异常
/// </summary>
public class KimiServiceException : Exception
{
    public int StatusCode { get; }
    public string? ResponseContent { get; }

    public KimiServiceException(string message, int statusCode, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
