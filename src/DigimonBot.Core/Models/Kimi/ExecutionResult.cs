namespace DigimonBot.Core.Models.Kimi;

/// <summary>
/// Kimi CLI 执行结果
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 标准输出内容
    /// </summary>
    public string Output { get; set; } = "";

    /// <summary>
    /// 错误输出内容
    /// </summary>
    public string Error { get; set; } = "";

    /// <summary>
    /// 进程退出码
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public int DurationMs { get; set; }
}
