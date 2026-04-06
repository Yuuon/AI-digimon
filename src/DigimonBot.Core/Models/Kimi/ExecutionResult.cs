namespace DigimonBot.Core.Models.Kimi;

/// <summary>
/// Kimi 执行结果
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应输出内容
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// 错误输出内容
    /// </summary>
    public string Error { get; set; } = "";

    /// <summary>
    /// 退出码（HTTP 状态码或 -1 表示异常）
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Kimi 会话ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Git 提交哈希（自动提交时填充）
    /// </summary>
    public string? CommitHash { get; set; }

    /// <summary>
    /// 是否已自动提交到 Git
    /// </summary>
    public bool Committed { get; set; }
}
